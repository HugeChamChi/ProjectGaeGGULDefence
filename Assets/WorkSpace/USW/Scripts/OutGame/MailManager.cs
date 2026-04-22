using UnityEngine;
using BackEnd;
using LitJson;
using System.Collections.Generic;

public class MailManager : MonoBehaviour
{
    public static MailManager Instance { get; private set; }

    private List<Post> _postList = new List<Post>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 우편 목록 조회. PostType.Admin = 관리자 우편 / PostType.Coupon = 쿠폰 우편
    /// </summary>
    public void GetPostList(PostType postType, System.Action onComplete = null)
    {
        if (!BackendManager.Instance.IsLoggedIn()) return;

        _postList.Clear();

        var bro = Backend.UPost.GetPostList(postType);

        if (!bro.IsSuccess())
        {
            Debug.LogError("우편 불러오기 실패 : " + bro);
            onComplete?.Invoke();
            return;
        }

        if (bro.GetFlattenJSON()["postList"].Count <= 0)
        {
            Debug.LogWarning("받을 우편이 없습니다.");
            onComplete?.Invoke();
            return;
        }

        foreach (JsonData postJson in bro.GetFlattenJSON()["postList"])
        {
            Post post = new Post();
            post.title   = postJson["title"].ToString();
            post.content = postJson["content"].ToString();
            post.inDate  = postJson["inDate"].ToString();
            _postList.Add(post);
            Debug.Log($"우편 - 제목: {post.title}, inDate: {post.inDate}");
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// 우편 개별 수령
    /// </summary>
    public void ReceivePost(PostType postType, int index)
    {
        if (index >= _postList.Count)
        {
            Debug.LogError("잘못된 우편 index입니다.");
            return;
        }

        var bro = Backend.UPost.ReceivePostItem(postType, _postList[index].inDate);

        if (bro.IsSuccess())
        {
            Debug.Log("우편 수령 성공 : " + bro);
            _postList.RemoveAt(index);
        }
        else
        {
            Debug.LogError("우편 수령 실패 : " + bro);
        }
    }

    /// <summary>
    /// 우편 전체 수령 (뒤끝 전체수령 API 없어 개별 반복)
    /// </summary>
    public void ReceiveAllPost(PostType postType)
    {
        if (_postList.Count <= 0)
        {
            Debug.LogWarning("수령할 우편이 없습니다.");
            return;
        }

        for (int i = _postList.Count - 1; i >= 0; i--)
        {
            var bro = Backend.UPost.ReceivePostItem(postType, _postList[i].inDate);

            if (bro.IsSuccess())
            {
                Debug.Log($"우편 수령 성공 : {_postList[i].title}");
                _postList.RemoveAt(i);
            }
            else
            {
                Debug.LogError($"우편 수령 실패 : {_postList[i].title} / " + bro);
            }
        }
    }

    /// <summary>
    /// 현재 캐시된 우편 목록 반환 (UI에서 사용)
    /// </summary>
    public List<Post> GetCurrentPostList() => _postList;
}
