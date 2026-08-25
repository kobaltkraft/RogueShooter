using UnityEngine;
namespace RogueArena.Player
{
    [RequireComponent(typeof(CharacterController),typeof(PlayerInputReader))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] float walkSpeed=6,sprintSpeed=9,crouchSpeed=3,jumpHeight=1.3,gravity=-25,mouseSensitivity=.12f;
        CharacterController controller; PlayerInputReader input; Transform view; float pitch,vertical,recoilPitch; float standingHeight=1.8f;
        public void Initialize(Transform cameraTransform){view=cameraTransform;}
        public void AddRecoil(float amount){recoilPitch=Mathf.Min(recoilPitch+amount,8f);}
        void Awake(){controller=GetComponent<CharacterController>();input=GetComponent<PlayerInputReader>();}
        void Start(){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        void Update(){ if(view==null)return; Vector2 look=input.Look*mouseSensitivity; pitch=Mathf.Clamp(pitch-look.y,-88,88); recoilPitch=Mathf.MoveTowards(recoilPitch,0,8f*Time.deltaTime); view.localRotation=Quaternion.Euler(pitch-recoilPitch,0,0); transform.Rotate(0,look.x,0); bool crouch=input.CrouchHeld; controller.height=Mathf.MoveTowards(controller.height,crouch?1.1f:standingHeight,5*Time.deltaTime); controller.center=Vector3.up*controller.height*.5f; float speed=crouch?crouchSpeed:(input.SprintHeld?sprintSpeed:walkSpeed); Vector2 m=input.Move; Vector3 motion=(transform.right*m.x+transform.forward*m.y)*speed; if(controller.isGrounded){if(vertical<0)vertical=-2;if(input.JumpPressed&&!crouch)vertical=Mathf.Sqrt(jumpHeight*-2*gravity);} vertical+=gravity*Time.deltaTime; motion.y=vertical; controller.Move(motion*Time.deltaTime); }
    }
}
